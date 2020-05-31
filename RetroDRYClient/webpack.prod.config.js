const path = require('path');
module.exports = {
    entry: './src/retrodry.ts',
    devtool: 'inline-source-map',
    mode: 'production',
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
        filename: 'retrodry.js',
        path: path.resolve(__dirname, 'dist')
    }
}