const path = require('path');
module.exports = {
    entry: './index.js',
    devtool: 'inline-source-map',
    mode: 'development',
    module: {
        rules: [
            {
                test: /\.tsx?$/,
                use: 'ts-loader',
                exclude: /node_modules/
            }
        ]
    },
    resolve: {
        extensions: [ '.tsx', '.ts', '.js']
    },
    output: {
        filename: 'retrodryclient.js',
        path: path.resolve(__dirname, 'dist'),
        library: 'retrodryclient',
        libraryTarget: 'umd'
    }
}